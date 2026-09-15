float4x4 wvp;
float4 palette[4];

struct VS_IN
{
	float4 position : POSITION;
	uint4 blendindices : BLENDINDICES;
	int texcoord : TEXCOORD;
};

struct VS_OUT
{
	float4 sv_position : SV_Position;
	float4 color : COLOR;
};

VS_OUT main(VS_IN i)
{
	VS_OUT o;

	float3 r0;
	o.sv_position.x = dot(i.position, transpose(wvp)[0]);
	o.sv_position.y = dot(i.position, transpose(wvp)[1]);
	o.sv_position.z = dot(i.position, transpose(wvp)[2]);
	o.sv_position.w = dot(i.position, transpose(wvp)[3]);
	r0.x = asfloat(asint(i.blendindices.y) & 255);
	r0.x = (float)asint(r0.x);
	r0.x = r0.x * 0.00392156886;
	r0.y = asfloat((uint)i.blendindices.x >> 4);
	r0.y = asfloat(asint(r0.y) & 3);
	r0.z = i.texcoord.x + 1;
	r0.z = (float)(int)r0.z;
	o.color = palette[r0.y] * r0.x + r0.z;

	return o;
}
