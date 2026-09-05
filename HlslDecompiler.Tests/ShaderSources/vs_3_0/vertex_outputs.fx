float4 fogColour : register(c5);
float pointSize : register(c4);
float4x4 wvp;

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
	o.fog = dot(transpose(wvp)[2], position);
	o.color = fogColour;

	return o;
}
