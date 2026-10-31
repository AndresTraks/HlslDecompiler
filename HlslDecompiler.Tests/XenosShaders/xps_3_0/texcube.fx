samplerCUBE s; sampler3D v; sampler1D o; float4 main(float3 a : TEXCOORD0) : COLOR { return texCUBE(s, a) + tex3D(v, a) + tex1D(o, a.x); }
