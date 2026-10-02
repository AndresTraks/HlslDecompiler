cbuffer Decode : register(b0)
{
	float3 lightDirection;
};

Texture2D encodedNormals;

float4 main(noperspective float4 sv_position : SV_Position) : SV_Target
{
	float2 t0 = 2 * encodedNormals.Load(int3((int2)sv_position.xy, 0)).xy - 1;
	float t1 = 1 - abs(t0.x) - abs(t0.y);
	float2 t2 = (t1 < 0 ? abs(t0.yx) * (float2)-sign(t0) : 0) + t0;
	float3 t3 = normalize(float3(t2, t1));
	float t4 = dot(t3, -lightDirection);
	return saturate(t4);
}
