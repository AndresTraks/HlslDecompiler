float4x4 wvp;
float pointSize;
float4 fogColour;

struct VS_OUT
{
	float4 position : POSITION;
	float psize : PSIZE;
	float fog : FOG;
	float4 color : COLOR;
};

VS_OUT main(float4 position : POSITION)
{
	VS_OUT o;

	o.position = mul(position, wvp);
	o.psize = pointSize;
	o.fog = mul(position, wvp).z;
	o.color = fogColour;

	return o;
}
