float4 arr[4];

struct VS_IN
{
	uint sv_vertexid : SV_VertexID;
	uint sv_instanceid : SV_InstanceID;
};

float4 main(VS_IN i) : SV_Position
{
	return arr[0] * (float4)i.sv_vertexid + (float4)i.sv_instanceid * arr[1];
}
