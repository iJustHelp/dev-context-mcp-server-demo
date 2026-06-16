using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.IO;

var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget/packages/formula.simplerepo/2.8.1/lib/net8.0/Formula.SimpleRepo.dll");
var asm = AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
var type = asm.GetType("Formula.SimpleRepo.ConnectionDetails");
Console.WriteLine(type.FullName);
Console.WriteLine("Constructors:");
foreach (var c in type.GetConstructors(BindingFlags.Public|BindingFlags.Instance)) Console.WriteLine(c);
Console.WriteLine("Properties:");
foreach (var p in type.GetProperties(BindingFlags.Public|BindingFlags.Instance)) Console.WriteLine(p.Name + " " + p.PropertyType.FullName);
Console.WriteLine("Methods:");
foreach (var m in type.GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly).OrderBy(m => m.Name))
    Console.WriteLine(m);
