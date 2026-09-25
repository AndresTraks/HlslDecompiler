float4x4 worldViewProj;

struct VS_IN
{
	float3 position : POSITION;
	uint color : COLOR;
};

struct VS_OUT
{
	float4 sv_position : SV_Position;
	float4 color : COLOR;
};

VS_OUT main(VS_IN i)
{
	VS_OUT o;

	int4 r0;
	int r1;
	r0.xyz = asint(i.position.xyz);
	r0.w = 1065353216;
	o.sv_position.x = dot(asfloat(r0), transpose(worldViewProj)[0]);
	o.sv_position.y = dot(asfloat(r0), transpose(worldViewProj)[1]);
	o.sv_position.z = dot(asfloat(r0), transpose(worldViewProj)[2]);
	o.sv_position.w = dot(asfloat(r0), transpose(worldViewProj)[3]);
	r0.x = (uint)i.color >> 8;
	r0.y = (uint)i.color >> 16;
	r0.xy = r0.xy & int2(255, 255);
	r0.xy = asint((float2)(uint2)r0.xy);
	r0.yz = asint(asfloat(r0.xy) * float2(0.00392156886, 0.00392156886));
	r1 = i.color & 255;
	r1 = asint((float)(uint)r1.x);
	r0.x = asint(asfloat(r1.x) * 0.00392156886);
	r1 = (uint)i.color >> 24;
	r1 = asint((float)(uint)r1.x);
	r0.w = asint(asfloat(r1.x) * 0.00392156886);
	o.color = asfloat(r0.w) * asfloat(r0);

	return o;
}
