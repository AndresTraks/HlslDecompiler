float3 axis : register(c4);
float4 planeA : register(c5);
float4 planeB : register(c6);
float4x4 worldViewProj;

struct VS_IN
{
	float4 position : POSITION;
	float4 normal : NORMAL;
};

struct VS_OUT
{
	float4 position : POSITION;
	float3 texcoord : TEXCOORD;
	float4 texcoord1 : TEXCOORD1;
};

VS_OUT main(VS_IN i)
{
	VS_OUT o;

	float4 r0;
	o.position.x = dot(i.position, transpose(worldViewProj)[0]);
	o.position.y = dot(i.position, transpose(worldViewProj)[1]);
	o.position.z = dot(i.position, transpose(worldViewProj)[2]);
	o.position.w = dot(i.position, transpose(worldViewProj)[3]);
	r0.xyz = axis.yzx * i.normal.zxy;
	o.texcoord = i.normal.yzx * axis.zxy + -r0.xyz;
	r0.x = dot(i.position, planeA);
	r0.y = (-r0.x < r0.x) ? 1 : 0;
	r0.z = (r0.x < -r0.x) ? 1 : 0;
	r0.x = saturate(r0.x);
	o.texcoord1.x = -r0.z + r0.y;
	r0.y = dot(i.position, planeB);
	r0.z = (-r0.y < r0.y) ? 1 : 0;
	r0.y = (r0.y < -r0.y) ? 1 : 0;
	o.texcoord1.y = -r0.y + r0.z;
	r0.yzw = -axis.xyz + i.position.xyz;
	r0.y = dot(r0.yzw, r0.yzw);
	r0.y = 1 / sqrt(r0.y);
	o.texcoord1.z = 1 / r0.y;
	r0.y = r0.x * -2 + 3;
	r0.x = r0.x * r0.x;
	o.texcoord1.w = r0.x * r0.y;

	return o;
}
