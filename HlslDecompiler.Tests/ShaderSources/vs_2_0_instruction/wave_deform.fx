float4 wave : register(c4);
float4x4 worldViewProjection;

struct VS_IN
{
	float4 position : POSITION;
	float4 texcoord : TEXCOORD;
};

struct VS_OUT
{
	float4 position : POSITION;
	float2 texcoord : TEXCOORD;
	float texcoord1 : TEXCOORD1;
};

VS_OUT main(VS_IN i)
{
	VS_OUT o;

	float4 r0;
	float2 r1;
	r0.x = i.position.x * wave.x + wave.y;
	r0.x = r0.x * 0.15915494 + 0.5;
	r0.x = frac(r0.x);
	r0.x = r0.x * 6.2831855 + -3.1415927;
	r1.y = sin(r0.x);
	r0.y = r1.y * wave.z + i.position.y;
	r1.x = r1.y * wave.z;
	o.texcoord1 = r1.x * wave.w;
	r0.xzw = i.position.xzw;
	o.position.x = dot(r0, transpose(worldViewProjection)[0]);
	o.position.y = dot(r0, transpose(worldViewProjection)[1]);
	o.position.z = dot(r0, transpose(worldViewProjection)[2]);
	o.position.w = dot(r0, transpose(worldViewProjection)[3]);
	o.texcoord = i.texcoord.xy;

	return o;
}
