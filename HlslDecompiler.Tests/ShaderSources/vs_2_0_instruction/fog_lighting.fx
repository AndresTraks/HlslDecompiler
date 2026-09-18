float4 fog : register(c9);
float4 lightColour : register(c8);
float4 lightDirection : register(c7);
float4x4 world : register(c4);
float4x4 worldViewProjection;

struct VS_IN
{
	float4 position : POSITION;
	float4 normal : NORMAL;
};

struct VS_OUT
{
	float4 color : COLOR;
	float fog : FOG;
	float4 position : POSITION;
	float psize : PSIZE;
};

VS_OUT main(VS_IN i)
{
	VS_OUT o;

	float3 r0;
	float3 r1;
	o.position.x = dot(i.position, transpose(worldViewProjection)[0]);
	o.position.y = dot(i.position, transpose(worldViewProjection)[1]);
	o.position.z = dot(i.position, transpose(worldViewProjection)[2]);
	r0.x = dot(i.normal.xyz, transpose(world)[0].xyz);
	r0.y = dot(i.normal.xyz, transpose(world)[1].xyz);
	r0.z = dot(i.normal.xyz, transpose(world)[2].xyz);
	r1 = normalize(r0.xyz);
	r0.x = dot(r1.xyz, -lightDirection.xyz);
	r0.x = max(r0.x, 0);
	r0.x = min(r0.x, 1);
	o.color = lightColour * r0.x + lightColour.w;
	r0.x = -fog.x + fog.y;
	r0.x = 1 / r0.x;
	r0.y = dot(i.position, transpose(worldViewProjection)[3]);
	r0.z = -r0.y + fog.y;
	r0.x = r0.x * r0.z;
	r0.x = max(r0.x, 0);
	o.fog = min(r0.x, 1);
	r0.x = 1 / r0.y;
	o.position.w = r0.y;
	o.psize = r0.x * fog.z;

	return o;
}
