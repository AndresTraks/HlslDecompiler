struct VS_IN
{
	float4 position : POSITION;
	float4 color : COLOR;
	float3 normal : NORMAL;
};

struct VS_OUT
{
	float4 position : POSITION;
	float4 color : COLOR;
	float4 color1 : COLOR1;
	float4 texcoord : TEXCOORD;
	float2 texcoord5 : TEXCOORD5;
	float psize : PSIZE;
	float fog : FOG;
};

VS_OUT main(VS_IN i)
{
	VS_OUT o;

	o.texcoord = float4(i.normal, 1);
	o.texcoord5 = i.normal.xy;
	o.color = i.color;
	o.color1 = i.color.wzyx;
	o.fog = i.position.w;
	o.position = i.position;
	o.psize = i.normal.z;

	return o;
}
