float4 v;
float4 ambient;

float4 main() : POSITION
{
	return ambient * lit(v.x, v.y, v.z).y + lit(v.x, v.y, v.z).z;
}
