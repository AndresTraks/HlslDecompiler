float4 offset : register(c6);
float scale : register(c7);
bool useOffset : register(c4);
bool useScale : register(c5);
float4x4 worldViewProj;

struct VS_IN
{
	float4 position : POSITION;
	float4 color : COLOR;
};

struct VS_OUT
{
	float4 position : POSITION;
	float4 color : COLOR;
};

VS_OUT main(VS_IN i)
{
	VS_OUT o;

	float4 r0;
	float3 r1;
	r0.x = useOffset.x;
	r0 = r0.x * offset + i.position;
	r1 = r0.xyz * scale.xxx + -r0.xyz;
	r0.xyz = useScale.xxx * r1.xyz + r0.xyz;
	o.position.x = dot(r0, transpose(worldViewProj)[0]);
	o.position.y = dot(r0, transpose(worldViewProj)[1]);
	o.position.z = dot(r0, transpose(worldViewProj)[2]);
	o.position.w = dot(r0, transpose(worldViewProj)[3]);
	r0 = -i.color.wzyx + i.color;
	o.color = useOffset.x * r0 + i.color.wzyx;

	return o;
}
