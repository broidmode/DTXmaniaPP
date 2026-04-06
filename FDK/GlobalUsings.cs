// Type aliases for D3D11 migration.
// These map short names to Vortice D3D11 equivalents so that most game/FDK
// code compiles with minimal changes.

// D3D11 types
global using Device = Vortice.Direct3D11.ID3D11Device;
global using Texture = FDK.ShaderResourceTexture;

// DirectInput types — Vortice uses IDirectInputDevice8 for all device types
global using DirectInput = Vortice.DirectInput.IDirectInput8;
global using Keyboard = Vortice.DirectInput.IDirectInputDevice8;
global using Mouse = Vortice.DirectInput.IDirectInputDevice8;
global using Joystick = Vortice.DirectInput.IDirectInputDevice8;

// DirectSound types — SharpDX used class names directly
global using DirectSound = Vortice.DirectSound.IDirectSound8;
global using SoundBuffer = Vortice.DirectSound.IDirectSoundBuffer;

// Math types — System.Numerics
global using Vector2 = System.Numerics.Vector2;
global using Vector3 = System.Numerics.Vector3;
global using Vector4 = System.Numerics.Vector4;
global using Matrix = System.Numerics.Matrix4x4;

// Color4 — Vortice.Mathematics provides this
global using Color4 = Vortice.Mathematics.Color4;

// Resolve Size ambiguity (System.Drawing.Size vs Vortice.Mathematics.Size)
global using Size = System.Drawing.Size;

// Result type — Vortice uses SharpGen.Runtime.Result
global using Result = SharpGen.Runtime.Result;
