struct VS_OUT
{
	float3 color : COLOR;
	float fog : FOG;
	float4 position : POSITION;
	float psize : PSIZE;
	float4 texcoord : TEXCOORD;
};

VS_OUT main()
{
	VS_OUT o;

	o.position = 0;
	o.texcoord = 0;
	o.color = 0;
	o.fog = 0;
	o.psize = 0;

	return o;
}
