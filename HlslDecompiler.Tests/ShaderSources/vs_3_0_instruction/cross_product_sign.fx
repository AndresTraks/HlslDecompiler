struct VS_IN
{
	float4 position : POSITION;
	float4 texcoord : TEXCOORD;
};

struct VS_OUT
{
	float4 position : POSITION;
	float4 texcoord : TEXCOORD;
};

VS_OUT main(VS_IN i)
{
	VS_OUT o;

	o.position.xyz = cross(i.position.xyz, i.texcoord.xyz);
	o.position.w = 7;
	o.texcoord = sign(i.texcoord);

	return o;
}
