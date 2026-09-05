float4 arr[4];

float4 main() : SV_Target
{
	return arr[0] + arr[2];
}
