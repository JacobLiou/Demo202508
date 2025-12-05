// See https://aka.ms/new-console-template for more information
using DnsClient;

Console.WriteLine("Hello, World!");

var lookup = new LookupClient();
var result = await lookup.QueryAsync("Google.com", QueryType.A);

var records = result.Answers.ARecords();
var ip = records.First().Address;
Console.WriteLine(ip);
