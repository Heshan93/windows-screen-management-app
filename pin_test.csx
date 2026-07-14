using System;
using System.Security.Cryptography;

var encodedHash = "PBKDF2$100000$DzkyCiNqU/YBD20ZGqfSqA==$GzYxInfO2CQvJNrs6/g6X7rWHQEh1T2RZSTt9kH3rQE=";
var parts = encodedHash.Split('$');
var iterations = int.Parse(parts[1]);
var salt = Convert.FromBase64String(parts[2]);
var expectedHash = Convert.FromBase64String(parts[3]);
var actualHash = Rfc2898DeriveBytes.Pbkdf2("1234", salt, iterations, HashAlgorithmName.SHA256, expectedHash.Length);
var match = CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
Console.WriteLine($"PIN 1234 matches stored hash: {match}");

// Generate a fresh hash for 1234
var freshSalt = RandomNumberGenerator.GetBytes(16);
var freshHash = Rfc2898DeriveBytes.Pbkdf2("1234", freshSalt, 100000, HashAlgorithmName.SHA256, 32);
var freshEncoded = $"PBKDF2$100000${Convert.ToBase64String(freshSalt)}${Convert.ToBase64String(freshHash)}";
Console.WriteLine($"Fresh hash for 1234: {freshEncoded}");

// Verify fresh hash
var freshParts = freshEncoded.Split('$');
var freshSalt2 = Convert.FromBase64String(freshParts[2]);
var freshExpected = Convert.FromBase64String(freshParts[3]);
var freshActual = Rfc2898DeriveBytes.Pbkdf2("1234", freshSalt2, 100000, HashAlgorithmName.SHA256, 32);
Console.WriteLine($"Fresh hash self-verify: {CryptographicOperations.FixedTimeEquals(freshActual, freshExpected)}");
