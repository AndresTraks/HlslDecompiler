struct PS_IN
{
	sample float4 color : COLOR;
	float4 sv_position : SV_Position;
};

float4 main(PS_IN i) : SV_Target
{
	return i.color;
}
