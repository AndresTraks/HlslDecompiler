uint count;
float4 a;

SamplerState linearClamp;
Texture2D sceneColor : register(t3);

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float4 r0;
	int r1;
	r0 = sceneColor.Sample(linearClamp, texcoord.xy);
	if (count != 0) {
		r0.x = r0.x + a.x;
		r1 = (1 >= count) ? -1 : 0;
		if (r1.x == 0) {
			r0.y = r0.y + a.y;
		}
	} else {
		r1 = -1;
	}
	if (r1.x == 0) {
		r0.z = r0.z + a.z;
	}
	o = r0;

	return o;
}
