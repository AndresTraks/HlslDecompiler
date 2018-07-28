struct VS_OUT
{
	float4 position : POSITION;
	float4 texcoord : TEXCOORD;
	float3 texcoord1 : TEXCOORD1;
	float3 texcoord2 : TEXCOORD2;
};

VS_OUT main() : POSITION
{
	VS_OUT o;

	o.position = 0;
	o.texcoord = 0;
	o.texcoord1 = 0;
	o.texcoord2 = 0;

	return o;
}
