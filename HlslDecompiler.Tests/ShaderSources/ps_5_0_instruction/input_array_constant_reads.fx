struct PS_IN
{
	float4 sv_position : SV_Position;
	float weights[3] : WEIGHTS;
	float2 texcoord : TEXCOORD;
};

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	o.x = i.texcoord.x + i.weights[0];
	o.z = i.texcoord.y * i.weights[2];
	o.y = i.weights[1];
	o.w = 1;

	return o;
}
