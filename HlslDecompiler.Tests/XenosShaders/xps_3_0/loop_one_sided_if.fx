float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float counter = texcoord.x;
	float4 v = texcoord;
	float sum = 0;
	float z = texcoord.z >= 0 ? 1 : 0;
	float flag = (texcoord.y >= 0 ? 1 : 0) * z;
	if (flag != 0) {
		counter = counter + 1;
	}
	for (int i = 0; i < 255 && flag != 0; i++) {
		if (flag != 0) {
			counter = counter - 0.5;
			sum = sum + dot(float3(0.25, 0.5, 0.75), v.xyw) * v.z;
			v = v + float4(0.25, 0.5, 1, 0.75);
			flag = -counter >= 0 ? 0 : 1;
		}
	}
	if (z != 0) {
		counter = 0.5;
	}
	return counter + sum;
}
