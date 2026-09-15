float4x4 wvp;
float4 threshold;

struct VS_IN
{
	float4 position : POSITION;
	float4 color : COLOR;
};

struct VS_OUT
{
	float4 sv_position : SV_Position;
	float4 color : COLOR;
};

VS_OUT main(VS_IN i)
{
	VS_OUT o;

	float4 r0;
	float4 r1;
	o.sv_position.x = dot(i.position, transpose(wvp)[0]);
	o.sv_position.y = dot(i.position, transpose(wvp)[1]);
	o.sv_position.z = dot(i.position, transpose(wvp)[2]);
	o.sv_position.w = dot(i.position, transpose(wvp)[3]);
	r0 = asfloat((threshold < i.color) ? -1 : 0);
	r1.xy = asfloat(asint(r0.zw) & asint(r0.xy));
	r0.xy = asfloat(asint(r0.zw) | asint(r0.xy));
	r0.x = asfloat(asint(r0.y) | asint(r0.x));
	r0.y = asfloat(asint(r1.y) & asint(r1.x));
	r1 = i.color * float4(0.5, 0.5, 0.5, 0.5);
	r1 = (asint(r0.y) != 0) ? i.color : r1;
	o.color = (asint(r0.x) != 0) ? r1 : float4(0, 0, 0, 1);

	return o;
}
