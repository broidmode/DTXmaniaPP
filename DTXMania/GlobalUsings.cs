// Type aliases for SharpDX → Vortice migration.
// These map the old SharpDX type names to their Vortice equivalents so that
// the vast majority of game/FDK code compiles unchanged.

// D3D9 types — SharpDX used short names, Vortice uses COM interface names
global using Direct3D = Vortice.Direct3D9.IDirect3D9;
global using Device = Vortice.Direct3D9.IDirect3DDevice9;
global using Texture = Vortice.Direct3D9.IDirect3DTexture9;
global using Surface = Vortice.Direct3D9.IDirect3DSurface9;
global using SwapChain = Vortice.Direct3D9.IDirect3DSwapChain9;
global using VertexBuffer = Vortice.Direct3D9.IDirect3DVertexBuffer9;
global using IndexBuffer = Vortice.Direct3D9.IDirect3DIndexBuffer9;
global using VertexDeclaration = Vortice.Direct3D9.IDirect3DVertexDeclaration9;
global using Query = Vortice.Direct3D9.IDirect3DQuery9;

// DirectInput types — SharpDX used class names directly
global using DirectInput = Vortice.DirectInput.IDirectInput8;

// DirectSound types — SharpDX used class names directly
global using DirectSound = Vortice.DirectSound.IDirectSound8;
global using SoundBuffer = Vortice.DirectSound.IDirectSoundBuffer;

// Math types — SharpDX.Mathematics → System.Numerics
global using Vector2 = System.Numerics.Vector2;
global using Vector3 = System.Numerics.Vector3;
global using Vector4 = System.Numerics.Vector4;
global using Matrix = System.Numerics.Matrix4x4;

// Color4 — SharpDX had its own; Vortice.Mathematics provides one
global using Color4 = Vortice.Mathematics.Color4;

// Resolve ambiguities (System.Drawing vs Vortice.Mathematics)
global using Size = System.Drawing.Size;
global using Color = System.Drawing.Color;
