float4x4 m;

struct VS_IN
{
	float4 position : POSITION;
	float4 normal : NORMAL;
	float4 color : COLOR;
	float4 texcoord3 : TEXCOORD3;
};

struct VS_OUT
{
	float3 texcoord2 : TEXCOORD2;
	float4 color1 : COLOR1;
	float fog : FOG;
	float4 position : POSITION;
};

VS_OUT main(VS_IN i)
{
	VS_OUT o;

	o.texcoord2 = i.normal.xyz * i.texcoord3.x;
	o.color1 = i.color;
	o.fog = i.texcoord3.y;
	o.position = mul(i.position, m);

	return o;
}
