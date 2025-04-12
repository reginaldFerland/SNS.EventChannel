using ChannelExample;
using System;
using System.Diagnostics;

// Create an instance of the GuidGenerator
GuidGenerator guidGenerator = new GuidGenerator();

Console.WriteLine("Generating 1 million GUIDs...");

// Start a stopwatch to measure the time
Stopwatch stopwatch = Stopwatch.StartNew();

// Generate 1 million GUIDs
var guids = GuidGenerator.GenerateOneMillionGuids();

// Stop the stopwatch
stopwatch.Stop();

Console.WriteLine($"Generated {guids.Count:N0} GUIDs in {stopwatch.ElapsedMilliseconds:N0} ms");
Console.WriteLine($"First GUID: {guids[0]}");
Console.WriteLine($"Last GUID: {guids[guids.Count - 1]}");
