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

	float r0;
	o.position.x = dot(position, transpose(wvp)[0]);
	o.position.y = dot(position, transpose(wvp)[1]);
	o.position.w = dot(position, transpose(wvp)[3]);
	r0 = dot(position, transpose(wvp)[2]);
	o.position.z = r0.x;
	o.fog = r0.x;
	o.psize = pointSize.x;
	o.color = fogColour;

	return o;
}
