#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$source = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\Extensions\AuthTokenDecoder.cs') -Raw
$tests = @'
namespace Extensions
{
	using System;
	using System.Collections.Generic;
	using System.Text;

	public static class AuthTokenDecoderTests
	{
		private static int assertions;

		public static void Run()
		{
			byte[] negotiate = Ntlm(1, 40, 0x02003203);
			negotiate[32] = 10;
			negotiate[39] = 15;
			negotiate = AddBuffer(negotiate, 16, Encoding.ASCII.GetBytes("DOMAIN"));
			negotiate = AddBuffer(negotiate, 24, Encoding.ASCII.GetBytes("WORKSTATION"));
			Check(negotiate, "NEGOTIATE", "DOMAIN", "WORKSTATION", "Version: 10.0.0", "OEM_DOMAIN_SUPPLIED");
			Check(Ntlm(1, 16, 0x200), "NEGOTIATE");
			Check(Convert.FromBase64String("TlRMTVNTUAABAAAAB4IIogAAAAAAAAAAAAAAAAAAAAAGAbEdAAAADw=="), "NEGOTIATE", "Version: 6.1.7601");
			Check(AddBuffer(Ntlm(3, 64, 0x201), 36, Text("\u00c5lice")), "\u00c5lice");

			byte[] av = Join(Av(1, Text("SERVER")), Av(2, Text("DOMAIN")), Av(3, Text("server.example.test")),
				Av(4, Text("example.test")), Av(5, Text("example.test")), Av(6, BitConverter.GetBytes(2u)),
				Av(7, BitConverter.GetBytes(132000000000000000L)), Av(8, new byte[48]),
				Av(9, Text("HTTP/server.example.test")), Av(10, new byte[16]), Av(0, new byte[0]));
			byte[] challenge = Ntlm(2, 56, 0x02800201);
			challenge = AddBuffer(challenge, 12, Text("DOMAIN"));
			challenge = AddBuffer(challenge, 40, av);
			Check(challenge, "CHALLENGE", "Server challenge", "SERVER", "server.example.test", "Target SPN", "Channel bindings hash", "Machine ID");
			byte[] legacy = Ntlm(2, 32, 0x201);
			legacy = AddBuffer(legacy, 12, Text("LONGDOMAINNAME"));
			Check(legacy, "CHALLENGE", "LONGDOMAINNAME");

			byte[] blob = new byte[28];
			blob[0] = blob[1] = 1;
			Buffer.BlockCopy(BitConverter.GetBytes(132000000000000000L), 0, blob, 8, 8);
			byte[] response = Join(new byte[16], blob, av, new byte[4]);
			byte[] authenticate = Ntlm(3, 88, 0x02080201);
			authenticate = AddBuffer(authenticate, 12, new byte[24]);
			authenticate = AddBuffer(authenticate, 20, response);
			authenticate = AddBuffer(authenticate, 28, Text("DOMAIN"));
			authenticate = AddBuffer(authenticate, 36, Text("alice"));
			authenticate = AddBuffer(authenticate, 44, Text("CLIENT"));
			authenticate = AddBuffer(authenticate, 52, new byte[16]);
			Check(authenticate, "AUTHENTICATE", "Username", "alice", "DOMAIN", "CLIENT", "Response format: NTLMv2", "MIC:", "NT proof string", "2019-");
			byte[] v1 = AddBuffer(Ntlm(3, 64, 0x80201), 20, new byte[24]);
			v1 = AddBuffer(v1, 12, new byte[24]);
			Check(v1, "NTLMv1 with extended session security", "Client challenge");
			Check(Ntlm(3, 64, 0x201), "Response format: empty");

			byte[] ntlmOid = Der(6, new byte[] { 43, 6, 1, 4, 1, 130, 55, 2, 2, 10 });
			byte[] kerberosOid = Der(6, new byte[] { 42, 134, 72, 134, 247, 18, 1, 2, 2 });
			byte[] spnegoOid = Der(6, new byte[] { 43, 6, 1, 5, 5, 2 });
			byte[] init = Der(0xa0, Seq(Der(0xa0, Seq(kerberosOid, ntlmOid)), Der(0xa1, Der(3, new byte[] { 1, 0x40 })), Der(0xa2, Der(4, negotiate))));
			Check(Der(0x60, Join(spnegoOid, init)), "SPNEGO", "NegTokenInit", "Offered mechanisms (not proof of selection)", "Kerberos", "NTLM", "WORKSTATION", "Request flags", "(mutual)");
			byte[] resp = Der(0xa1, Seq(Der(0xa0, Der(10, new byte[] { 1 })), Der(0xa1, ntlmOid), Der(0xa2, Der(4, authenticate)), Der(0xa3, Der(4, new byte[] { 1, 2, 3 }))));
			Check(resp, "NegTokenResp", "Selected mechanism: NTLM", "accept-incomplete", "alice", "Mechanism list MIC");
			Check(Der(0xa1, Seq(Der(0xa0, Der(10, new byte[] { 0 })))), "accept-completed");
			Check(Der(0xa1, Seq(Der(0xa2, Der(4, new byte[0])))), "(empty)");
			Check(Der(0xa0, Seq(Der(0xa3, Seq(Der(0xa0, Der(27, Encoding.ASCII.GetBytes("hint.example.test"))))), Der(0xa4, Der(4, new byte[] { 1 })))), "Negotiation hints", "hint.example.test", "Mechanism list MIC");

			byte[] encrypted = Seq(ContextInt(0, 18), ContextInt(1, 2), Der(0xa2, Der(4, new byte[] { 1, 2, 3, 4 })));
			byte[] principal = Seq(ContextInt(0, 2), Der(0xa1, Seq(KString("HTTP"), KString("server.example.test"))));
			byte[] ticket = Der(0x61, Seq(ContextInt(0, 5), Der(0xa1, KString("EXAMPLE.TEST")), Der(0xa2, principal), Der(0xa3, encrypted)));
			byte[] apreq = Der(0x6e, Seq(ContextInt(0, 5), ContextInt(1, 14), Der(0xa2, Der(3, new byte[] { 0, 0x20, 0, 0, 0 })), Der(0xa3, ticket), Der(0xa4, encrypted)));
			byte[] kerberos = Der(0x60, Join(kerberosOid, new byte[] { 1, 0 }, apreq));
			Check(kerberos, "GSS-API mechanism: Kerberos", "AP-REQ", "Realm: EXAMPLE.TEST", "Principal: HTTP/server.example.test", "Encryption type: 18 (aes256-cts-hmac-sha1-96)", "decryption keys required", "cannot be recovered without decryption keys", "(mutual-required)");
			Check(Der(0xa0, Seq(Der(0xa0, Seq(kerberosOid, ntlmOid)), Der(0xa2, Der(4, kerberos)))), "Offered mechanisms", "GSS-API mechanism: Kerberos", "HTTP/server.example.test");
			byte[] aprep = Der(0x6f, Seq(ContextInt(0, 5), ContextInt(1, 15), Der(0xa2, encrypted)));
			Check(Der(0xa1, Seq(Der(0xa2, Der(4, Join(new byte[] { 2, 0 }, aprep))))), "Kerberos token ID: 0x0200", "AP-REP");
			byte[] error = Der(0x7e, Seq(ContextInt(0, 5), ContextInt(1, 30), ContextInt(6, 7), Der(0xa7, KString("EXAMPLE.TEST")), Der(0xa8, Seq(ContextInt(0, 1), Der(0xa1, Seq(KString("alice")))))));
			Check(error, "KRB-ERROR", "Client realm", "Client principal", "Principal: alice");

			Malformed(new byte[0]);
			Malformed(new byte[] { 0x60, 0x80 });
			Malformed(new byte[] { 0x60, 0x84, 0xff, 0xff, 0xff, 0xff });
			Malformed(new byte[] { 0xa0, 2, 0x30, 2 });
			byte[] badOffset = (byte[])authenticate.Clone();
			Put32(badOffset, 40, 0xffffffff);
			Malformed(badOffset);
			byte[] badUnicode = AddBuffer(Ntlm(3, 64, 0x201), 36, new byte[] { 65 });
			Malformed(badUnicode);
			byte[] missingMic = AddBuffer(Ntlm(3, 64, 0x201), 20, response);
			Malformed(missingMic);
			Malformed(Der(0x60, Der(6, new byte[] { 0x80 })));
			byte[] nested = Der(4, new byte[0]);
			for (int i = 0; i < 40; i++) nested = Seq(nested);
			Malformed(nested);

			var random = new Random(20261015);
			foreach (byte[] valid in new[] { negotiate, challenge, authenticate, init, resp, kerberos, error })
			{
				for (int length = 0; length < valid.Length; length++) Decode(Subarray(valid, length));
				for (int i = 0; i < 1000; i++)
				{
					byte[] mutation = (byte[])valid.Clone();
					mutation[random.Next(mutation.Length)] = (byte)random.Next(256);
					Decode(mutation);
				}
			}
			for (int i = 0; i < 10000; i++)
			{
				byte[] input = new byte[random.Next(512)];
				random.NextBytes(input);
				Decode(input);
			}
			Console.WriteLine("Auth token decoder: " + assertions + " assertions and malformed-input mutation coverage passed.");
		}

		private static string Decode(byte[] input)
		{
			var output = new StringBuilder();
			AuthTokenDecoder.Append(output, input);
			return output.ToString();
		}

		private static void Check(byte[] input, params string[] expected)
		{
			string output = Decode(input);
			if (output.Contains("Malformed authentication token")) throw new Exception(output);
			foreach (string value in expected)
			{
				assertions++;
				if (!output.Contains(value)) throw new Exception("Missing: " + value + Environment.NewLine + output);
			}
		}

		private static void Malformed(byte[] input)
		{
			assertions++;
			if (!Decode(input).Contains("Malformed authentication token")) throw new Exception("Expected malformed-token diagnostic.");
		}

		private static byte[] Ntlm(int type, int length, uint flags)
		{
			byte[] data = new byte[length];
			Buffer.BlockCopy(Encoding.ASCII.GetBytes("NTLMSSP\0"), 0, data, 0, 8);
			Put32(data, 8, (uint)type);
			Put32(data, type == 1 ? 12 : type == 2 ? 20 : 60, flags);
			return data;
		}

		private static byte[] AddBuffer(byte[] data, int field, byte[] value)
		{
			int offset = data.Length;
			data = Join(data, value);
			data[field] = data[field + 2] = (byte)value.Length;
			data[field + 1] = data[field + 3] = (byte)(value.Length >> 8);
			Put32(data, field + 4, (uint)offset);
			return data;
		}

		private static void Put32(byte[] data, int offset, uint value) => Buffer.BlockCopy(BitConverter.GetBytes(value), 0, data, offset, 4);
		private static byte[] Text(string text) => Encoding.Unicode.GetBytes(text);
		private static byte[] KString(string text) => Der(27, Encoding.UTF8.GetBytes(text));
		private static byte[] ContextInt(int field, byte value) => Der(0xa0 + field, Der(2, new byte[] { value }));
		private static byte[] Seq(params byte[][] fields) => Der(0x30, Join(fields));
		private static byte[] Av(int id, byte[] value) => Join(new byte[] { (byte)id, 0, (byte)value.Length, (byte)(value.Length >> 8) }, value);

		private static byte[] Join(params byte[][] values)
		{
			var result = new List<byte>();
			foreach (byte[] value in values) result.AddRange(value);
			return result.ToArray();
		}

		private static byte[] Subarray(byte[] data, int length)
		{
			byte[] result = new byte[length];
			Buffer.BlockCopy(data, 0, result, 0, length);
			return result;
		}

		private static byte[] Der(int tag, byte[] value)
		{
			byte[] length = value.Length < 128 ? new byte[] { (byte)value.Length } :
				value.Length < 256 ? new byte[] { 0x81, (byte)value.Length } : new byte[] { 0x82, (byte)(value.Length >> 8), (byte)value.Length };
			return Join(new byte[] { (byte)tag }, length, value);
		}
	}
}
'@
Add-Type -TypeDefinition ($source + [Environment]::NewLine + $tests)
[Extensions.AuthTokenDecoderTests]::Run()
