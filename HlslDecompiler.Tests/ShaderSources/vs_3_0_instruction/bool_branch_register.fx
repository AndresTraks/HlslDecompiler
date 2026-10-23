float3 eye : register(c5);
sampler2D heightMap;
float4 morph : register(c4);
bool useMorph : register(b0);
float4x4 viewProj;

struct VS_OUT
{
	float4 position : POSITION;
	float2 texcoord : TEXCOORD;
	float texcoord1 : TEXCOORD1;
};

VS_OUT main(float4 position : POSITION)
{
	VS_OUT o;

	float4 r0;
	float4 r1;
	float4 r2;
	r0.xy = morph.zz * position.xy;
	r0.zw = 0;
	r1 = tex2Dlod(heightMap, r0);
	r1.y = r1.x * morph.w;
	if (useMorph) {
		r1.xz = position.xy;
		r1.xzw = r1.xyz + -eye.xyz;
		r0.z = dot(r1.xzw, r1.xzw);
		r0.z = rsqrt(r0.z);
		r0.z = 1 / r0.z;
		r0.z = r0.z + -morph.x;
		r0.w = 1 / morph.y;
		r0.z = saturate(r0.w * r0.z);
		r1.xz = 0.5 * position.xy;
		r2.xy = frac(r1.xz);
		r1.xz = r1.xz + -r2.xy;
		r1.xz = r1.xz * 2 + -position.xy;
		r2.xy = r0.zz * r1.xz + position.xy;
	} else {
		r2.xy = position.xy;
	}
	r2.zw = r1.yy * float2(1, 0) + float2(0, 1);
	o.position.x = dot(r2.xzyw, transpose(viewProj)[0]);
	o.position.y = dot(r2.xzyw, transpose(viewProj)[1]);
	o.position.z = dot(r2.xzyw, transpose(viewProj)[2]);
	o.position.w = dot(r2.xzyw, transpose(viewProj)[3]);
	o.texcoord = r0.xy;
	o.texcoord1 = r1.y;

	return o;
}
