float4 first;
float4 arr[8];
int idx;

float4 main() : SV_Position
{
	return arr[idx] + first;
}
