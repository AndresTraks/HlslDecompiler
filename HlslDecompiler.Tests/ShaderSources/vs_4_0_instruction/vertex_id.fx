float4 arr[4];

struct VS_IN
{
	uint sv_vertexid : SV_VertexID;
	uint sv_instanceid : SV_InstanceID;
};

float4 main(VS_IN i) : SV_Position
{
	float4 o;

	float4 r0;
	float r1;
	r0.x = i.sv_instanceid.x;
	r0 = r0.x * arr[1];
	r1 = i.sv_vertexid.x;
	o = arr[0] * r1.x + r0;

	return o;
}
