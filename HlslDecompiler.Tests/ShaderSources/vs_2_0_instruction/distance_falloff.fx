float3 attenuation : register(c5);
float4 light : register(c4);
float4x4 worldViewProj;

struct VS_IN
{
	float4 position : POSITION;
	float4 normal : NORMAL;
};

struct VS_OUT
{
	float4 color : COLOR;
	float4 position : POSITION;
};

VS_OUT main(VS_IN i)
{
	VS_OUT o;

	float4 r0;
	float4 r1;
	float3 r2;
	o.position.x = dot(i.position, transpose(worldViewProj)[0]);
	o.position.y = dot(i.position, transpose(worldViewProj)[1]);
	o.position.z = dot(i.position, transpose(worldViewProj)[2]);
	o.position.w = dot(i.position, transpose(worldViewProj)[3]);
	r0.xyz = -i.position.xyz + light.xyz;
	r0.w = dot(r0.xyz, r0.xyz);
	r0.w = 1 / sqrt(r0.w);
	r0.xyz = r0.www * r0.xyz;
	r1.z = 1 / r0.w;
	r0.x = dot(r0.xyz, i.normal.xyz);
	r0.y = (-r0.x < r0.x) ? 1 : 0;
	r0.x = (r0.x < -r0.x) ? 1 : 0;
	r0.x = -r0.x + r0.y;
	r2.xy = r1.zz * r1.zz;
	r2.z = 1;
	r1.w = 1;
	r0.yzw = r1.zwz * r2.xyz;
	o.color.zw = r1.zw;
	r0.y = dot(r0.yzw, attenuation.xyz);
	r0.y = 1 / r0.y;
	o.color.x = r0.x * r0.y;
	r0.x = max(r0.y, 0);
	r0.x = min(r0.x, 1);
	r0.x = log2(r0.x);
	r0.x = r0.x * light.w;
	o.color.y = exp2(r0.x);

	return o;
}
