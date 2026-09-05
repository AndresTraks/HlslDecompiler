float4 arr[4];

float4 main() : SV_Target
{
	float4 o;

	o = arr[0] + arr[2];

	return o;
}
