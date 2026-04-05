using System;
using System.Linq;

var t = typeof(Vortice.Direct3D9.TransformState);
Console.WriteLine("TransformState members:");
foreach (var name in Enum.GetNames(t).OrderBy(n => n))
    Console.WriteLine("  {0} = {1}", name, (int)Enum.Parse(t, name));

var devType = typeof(Vortice.Direct3D9.IDirect3DDevice9);
var drawMethods = devType.GetMethods().Where(m => m.Name.Contains("Draw")).ToArray();
Console.WriteLine("\nDraw methods:");
foreach (var m in drawMethods)
    Console.WriteLine("  {0}({1})", m.Name, string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name)));

var caps = devType.GetMethods().Where(m => m.Name.Contains("Caps")).ToArray();
Console.WriteLine("\nCaps methods:");
foreach (var m in caps)
    Console.WriteLine("  {0}({1})", m.Name, string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name)));

var lockMethods = typeof(Vortice.Direct3D9.IDirect3DTexture9).GetMethods().Where(m => m.Name.Contains("Lock") || m.Name.Contains("Unlock")).ToArray();
Console.WriteLine("\nTexture Lock/Unlock methods:");
foreach (var m in lockMethods)
    Console.WriteLine("  {0}({1}) -> {2}", m.Name, string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name)), m.ReturnType.Name);

var color4Type = typeof(Vortice.Mathematics.Color4);
Console.WriteLine("\nColor4 all members:");
foreach (var m in color4Type.GetMembers(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly))
    Console.WriteLine("  {0} [{1}]", m.Name, m.MemberType);
Console.WriteLine("\nColor4 constructors:");
foreach (var c in color4Type.GetConstructors())
    Console.WriteLine("  ({0})", string.Join(", ", c.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name)));

// Test Color4 access
var c4 = new Vortice.Mathematics.Color4(1.0f, 0.5f, 0.25f, 0.75f);
Console.WriteLine("\nColor4 test: R={0}, G={1}, B={2}, A={3}", c4.R, c4.G, c4.B, c4.A);

var toMethods = color4Type.GetMethods().Where(m => m.Name.StartsWith("To")).ToArray();
Console.WriteLine("\nColor4 To* methods:");
foreach (var m in toMethods)
    Console.WriteLine("  {0}() -> {1}", m.Name, m.ReturnType.Name);
