cbuffer C : register(b0)
{
	float4x4 invViewProj;
	float4x4 decalInv;
	uint tile;
	uint tilesPerRow;
	float fadeAngle;
};

SamplerState s;
Texture2D<float> depth;
Texture2D atlas;

struct PS_IN
{
	noperspective float4 sv_position : SV_Position;
	float2 texcoord : TEXCOORD;
	float3 normal : NORMAL;
};

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	int4 r0;
	float4 r1;
	int3 r2;
	int3 r3;
	r0.xy = (int2)i.sv_position.xy;
	r0.zw = int2(0, 0);
	r0.z = asint(depth.Load(r0.xyz).x);
	r0.xy = asint(i.texcoord.xy * float2(2, -2) + float2(-1, 1));
	r0.w = 1065353216;
	r1.x = dot(asfloat(r0), transpose(invViewProj)[0]);
	r1.y = dot(asfloat(r0), transpose(invViewProj)[1]);
	r1.z = dot(asfloat(r0), transpose(invViewProj)[2]);
	r1.w = dot(asfloat(r0), transpose(invViewProj)[3]);
	r0 = asint(r1 / r1.w);
	r1.y = dot(asfloat(r0), transpose(decalInv)[1]);
	r1.x = dot(asfloat(r0), transpose(decalInv)[0]);
	r1.z = dot(asfloat(r0), transpose(decalInv)[2]);
	r2 = asint(-(abs(r1.xyz)) + float3(0.5, 0.5, 0.5));
	r2 = (asfloat(r2.xyz) < float3(0, 0, 0)) ? -1 : 0;
	r0.w = r2.y | r2.x;
	r0.w = r2.z | r0.w;
	if (r0.w != 0) discard;
	r2 = asint(ddy_coarse(asfloat(r0.zxy)));
	r0.xyz = asint(ddx_coarse(asfloat(r0.yzx)));
	r3 = asint(asfloat(r0.xyz) * asfloat(r2.xyz));
	r0.xyz = asint(asfloat(r2.zxy) * asfloat(r0.yzx) + -(asfloat(r3.xyz)));
	r0.w = asint(dot(asfloat(r0.xyz), asfloat(r0.xyz)));
	r0.w = asint(rsqrt(asfloat(r0.w)));
	r0.xyz = asint(asfloat(r0.www) * asfloat(r0.xyz));
	r0.x = asint(dot(asfloat(r0.xyz), i.normal.xyz));
	r0.x = asint(asfloat(r0.x) + -(fadeAngle));
	r0.y = asint(-(fadeAngle) + 1);
	r0.x = asint(saturate(asfloat(r0.x) / asfloat(r0.y)));
	r2.x = (uint)tile / (uint)tilesPerRow;
	r3.x = (uint)tile % (uint)tilesPerRow;
	r3.x = asint((float)(uint)r3.x);
	r3.y = asint((float)(uint)r2.x);
	r0.yz = asint(r1.xz + asfloat(r3.xy));
	r0.yz = asint(asfloat(r0.yz) + float2(0.5, 0.5));
	r0.w = asint((float)(uint)tilesPerRow);
	r0.yz = asint(asfloat(r0.yz) / asfloat(r0.ww));
	r1 = atlas.Sample(s, asfloat(r0.yz));
	o.w = asfloat(r0.x) * r1.w;
	o.xyz = r1.xyz;

	return o;
}
