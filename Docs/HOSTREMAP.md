# Host remapping

Use **Rules > HOSTS...** to enable host remapping. Enter one rule per line,
with the destination host or IP address first and the requested hostname second:

```text
# Send requests for example.test to a local server
127.0.0.1 example.test
127.0.0.1:8080 other.example.test
```

Fields are separated by whitespace. Blank lines and lines beginning with `#`
are ignored. Incomplete lines are logged and skipped. Hostnames match exactly,
without regard to case; the first matching rule wins. Any fields after the second
are ignored. A match sets the exchange's `x-overridehost` flag to the first token;
existing override handling determines how it is used. Imported Windows HOSTS
files can be used, but only the second token of each rule is matched.
