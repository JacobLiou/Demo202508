// See https://aka.ms/new-console-template for more information
Console.WriteLine("Hello, World!");

var file = System.Reflection.Assembly.LoadFrom(@"C:\Program Files (x86)\DevExpress 19.2\Components\Bin\Framework\DevExpress.Utils.v19.2.dll").FullName;
Console.WriteLine(file);