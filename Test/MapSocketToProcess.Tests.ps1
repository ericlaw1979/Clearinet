#Requires -Version 5.1
param([string]$BuildDirectory = (Join-Path $PSScriptRoot '..\Clearinet\bin\Debug'))
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Desktop') {
	throw 'Run with powershell.exe -NoProfile -File .\Test\MapSocketToProcess.Tests.ps1'
}
$corePath = Join-Path (Resolve-Path -LiteralPath $BuildDirectory).Path 'Clearinet.exe'
$null = [Reflection.Assembly]::LoadFrom($corePath)
$tests = @'
using System;
using System.Collections;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Clearinet.CaptureEngine;

public static class MapSocketToProcessTests
{
	const BindingFlags Members = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
	static int assertions;
	static void Check(bool condition, string message)
	{
		assertions++;
		if (!condition) throw new Exception(message);
	}
	static object Field(object target, string name)
	{
		return target.GetType().GetField(name, Members).GetValue(target);
	}
	static long Timestamp(object table) { return (long)Field(table, "FetchedAt"); }
	static bool HasSnapshot(object table) { return (bool)Field(table, "HasSnapshot"); }
	static string ExpectedOwner()
	{
		using (Process process = Process.GetCurrentProcess())
			return process.ProcessName + ":" + process.Id;
	}
	static void CheckFamily(MapSocketToProcess mapper, IPAddress address, bool ipv6)
	{
		object table = Field(mapper, ipv6 ? "_ipv6Table" : "_ipv4Table");
		object other = Field(mapper, ipv6 ? "_ipv4Table" : "_ipv6Table");
		long otherFetchedAt = Timestamp(other);
		var listener = new TcpListener(address, 0);
		listener.Start();
		try
		{
			using (var first = new TcpClient(address.AddressFamily))
			{
				first.Connect((IPEndPoint)listener.LocalEndpoint);
				using (TcpClient accepted = listener.AcceptTcpClient())
				{
					var endpoint = (IPEndPoint)accepted.Client.RemoteEndPoint;
					Check(mapper.MapSocketToOriginatingProcess(endpoint) == ExpectedOwner(), "Initial lookup identifies the client: " + address);
					Check(HasSnapshot(table), "Requested family has a snapshot: " + address);
					Check(Timestamp(other) == otherFetchedAt, "Initial lookup does not refresh the other family: " + address);
					long fetchedAt = Timestamp(table);
					Check(mapper.MapSocketToOriginatingProcess(endpoint) == ExpectedOwner(), "Cached lookup identifies the client: " + address);
					Check(Timestamp(table) == fetchedAt, "Cache hit does not refresh: " + address);
					if (!ipv6)
					{
						var mapped = new IPEndPoint(endpoint.Address.MapToIPv6(), endpoint.Port);
						Check(mapper.MapSocketToOriginatingProcess(mapped) == ExpectedOwner(), "IPv4-mapped IPv6 endpoint resolves through IPv4");
						Check(Timestamp(table) == fetchedAt && Timestamp(other) == otherFetchedAt, "Mapped endpoint uses the existing IPv4 snapshot only");
					}
					using (var second = new TcpClient(address.AddressFamily))
					{
						second.Connect((IPEndPoint)listener.LocalEndpoint);
						using (TcpClient secondAccepted = listener.AcceptTcpClient())
						{
							var newEndpoint = (IPEndPoint)secondAccepted.Client.RemoteEndPoint;
							Check(mapper.MapSocketToOriginatingProcess(newEndpoint) == ExpectedOwner(), "Cache miss refresh discovers a connection created after the snapshot: " + address);
							Check(Timestamp(table) > fetchedAt, "Cache miss refreshes the requested family: " + address);
							Check(Timestamp(other) == otherFetchedAt, "Cache miss leaves the other family untouched: " + address);
							fetchedAt = Timestamp(table);
							Check(mapper.MapSocketToOriginatingProcess(newEndpoint) == ExpectedOwner() && Timestamp(table) == fetchedAt, "Refreshed entry is cached: " + address);
						}
					}
					Check(mapper.MapSocketToOriginatingProcess(new IPEndPoint(address, 0)) == null, "Missing port still returns null after refreshing: " + address);
					Check(Timestamp(table) > fetchedAt && Timestamp(other) == otherFetchedAt, "Unresolved miss refreshes only the requested family: " + address);
					fetchedAt = Timestamp(table);
					var owners = (IDictionary)Field(table, "Owners");
					object key = Activator.CreateInstance(owners.GetType().GetGenericArguments()[0],
						new object[] { address.GetAddressBytes(), 0, ipv6, (uint)0, endpoint.Port });
					owners[key] = uint.MaxValue;
					Check(mapper.MapSocketToOriginatingProcess(endpoint) == null && Timestamp(table) == fetchedAt, "Ambiguous owner remains null without a missing-endpoint refresh: " + address);
					table.GetType().GetField("FetchedAt", Members).SetValue(table, Stopwatch.GetTimestamp() - Stopwatch.Frequency * 601L);
					Check(mapper.MapSocketToOriginatingProcess(endpoint) == ExpectedOwner(), "Expired snapshot refreshes before lookup: " + address);
					Check(Timestamp(table) > fetchedAt && Timestamp(other) == otherFetchedAt, "Expiry refresh leaves the other family intact: " + address);
				}
			}
		}
		finally { listener.Stop(); }
	}
	public static void Run()
	{
		var mapper = new MapSocketToProcess(TimeSpan.FromMinutes(10));
		CheckFamily(mapper, IPAddress.Loopback, false);
		Check(!HasSnapshot(Field(mapper, "_ipv6Table")), "IPv4 operations never fetch the IPv6 table");
		if (!Socket.OSSupportsIPv6) throw new Exception("IPv6 support is required to validate both address families.");
		CheckFamily(mapper, IPAddress.IPv6Loopback, true);
		var uncached = new MapSocketToProcess(TimeSpan.Zero);
		var missing = new IPEndPoint(IPAddress.Loopback, 0);
		Check(uncached.MapSocketToOriginatingProcess(missing) == null, "Zero-duration missing lookup returns null");
		long fetchedAt = Timestamp(Field(uncached, "_ipv4Table"));
		Check(uncached.MapSocketToOriginatingProcess(missing) == null && Timestamp(Field(uncached, "_ipv4Table")) > fetchedAt,
			"Zero duration refreshes on each lookup");
		Check(!HasSnapshot(Field(uncached, "_ipv6Table")), "Zero duration still fetches only the requested family");
		Console.WriteLine("Socket-to-process assertions passed: " + assertions);
	}
}
'@
$compiler = New-Object CodeDom.Compiler.CompilerParameters
$compiler.ReferencedAssemblies.AddRange([string[]]@('System.dll', 'System.Core.dll', $corePath))
Add-Type -TypeDefinition $tests -CompilerParameters $compiler
[MapSocketToProcessTests]::Run()
