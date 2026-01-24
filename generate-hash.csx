#!/usr/bin/env dotnet-script
#r "nuget: BCrypt.Net-Next, 4.0.3"

using BCrypt.Net;

var password = "admin";
var hash = BCrypt.HashPassword(password, 12);

Console.WriteLine($"Password: {password}");
Console.WriteLine($"Hash: {hash}");
Console.WriteLine($"Verify: {BCrypt.Verify(password, hash)}");
