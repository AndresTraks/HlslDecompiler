float4 first;
float4 arr[8];
int idx;

float4 main() : SV_Position
{
	float4 o;

	float r0;
	r0 = idx;
	o = first + arr[r0.x];

	return o;
}
