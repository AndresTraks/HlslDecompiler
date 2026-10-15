struct VS_IN
{
	uint sv_vertexid : SV_VertexID;
	uint sv_instanceid : SV_InstanceID;
};

float4 main(VS_IN i) : SV_Position
{
	float4 o;

	int3 r0;
	r0.x = i.sv_vertexid & 1;
	r0.z = (uint)i.sv_vertexid >> 1;
	r0.xy = asint((float2)(uint2)r0.xz);
	o.xy = asfloat(r0.xy) * float2(2, 2) + float2(-1, -1);
	o.z = asfloat(i.sv_instanceid | 1056964608);
	o.w = 1;

	return o;
}
