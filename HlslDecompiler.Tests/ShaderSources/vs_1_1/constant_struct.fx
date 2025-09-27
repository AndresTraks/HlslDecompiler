struct VS_OUT
{
	float4 position : POSITION;
	float4 texcoord : TEXCOORD;
	float3 color : COLOR;
	float psize : PSIZE;
	float fog : FOG;
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
