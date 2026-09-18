float4 packing;

SamplerState samp;
Texture2D source;

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float2 t0 = source.Sample(samp, texcoord).xy;
	int t1 = asint(t0.y * packing.x);
	int4 t2 = int4(t1 & int2(8388607, 2147483647), asint(t0.x * packing.x) & int2(8388607, 2147483647));
	int t3 = asint(t0.x * packing.x);
	int2 t4 = 113 - (int2(((uint)t1 >> 23) & 255, ((uint)t3 >> 23) & 255));
	int t5 = ((((uint)t1 >> 16) & 32768) + (((t1 & 2139095040) == 2139095040 ? t2.x ? ((t1 | (((uint)t1 >> 13) | ((uint)t1 >> 3))) & 1023) + 31744 : 31744 : t2.y > 1207951360 ? 31743 : t2.y < 947912704 ? (uint)(t4.x < 24 ? (uint)t2.x + 8388608 >> t4.x : 0) >> 13 : (uint)t2.y - 939524096 >> 13) & 32767)) * 65536;
	int t6 = (uint)t5 >> 16;
	int2 t7 = t6 & int2(31744, 1023);
	int t8 = (t6 * 8192) & 8380416;
	int t9 = (uint)t7.y >> 8;
	int t10 = t9 ? t9 : t7.y;
	int t11 = (uint)t10 >> 4;
	int t12 = t11 ? t11 : t10;
	int t13 = (uint)t12 >> 2;
	int t14 = t11 ? t9 ? 12 : 4 : t9 ? 8 : 0;
	int t15 = t13 ? t14 + 2 : t14;
	int t16 = t7.y ? 10 - ((uint)(t13 ? t13 : t12) >> 1 ? t15 + 1 : t15) : 11;
	int t17 = (((uint)t3 >> 16) & 32768) + (((t3 & 2139095040) == 2139095040 ? t2.z ? ((t3 | (((uint)t3 >> 13) | ((uint)t3 >> 3))) & 1023) + 31744 : 31744 : t2.w > 1207951360 ? 31743 : t2.w < 947912704 ? (uint)(t4.y < 24 ? (uint)t2.z + 8388608 >> t4.y : 0) >> 13 : (uint)t2.w - 939524096 >> 13) & 32767);
	int t18 = t5 + t17;
	int3 t19 = t18 & int3(31744, 65535, 1023);
	int t20 = (t19.y * 8192) & 8380416;
	int t21 = (uint)t19.z >> 8;
	int t22 = t21 ? t21 : t19.z;
	int t23 = (uint)t22 >> 4;
	int t24 = t23 ? t23 : t22;
	int t25 = (uint)t24 >> 2;
	int t26 = t23 ? t21 ? 12 : 4 : t21 ? 8 : 0;
	int t27 = t25 ? t26 + 2 : t26;
	int t28 = t19.z ? 10 - ((uint)(t25 ? t25 : t24) >> 1 ? t27 + 1 : t27) : 11;
	return float4(asfloat(((t17 * 65536) & -2147483648) + ((t19.x ? t19.x == 31744 ? t20 + 2139095040 : ((((uint)t19.y >> 10) * 8388608) & 260046848) + 939524096 + t20 : t19.z ? 947912704 - (t28 * 8388608) + (((t19.z << t28) * 8192) & 8380416) : 0) & 2147475456)), asfloat((t5 & -2147483648) + ((t7.x ? t7.x == 31744 ? t8 + 2139095040 : ((((uint)t6 >> 10) * 8388608) & 260046848) + 939524096 + t8 : t7.y ? 947912704 - (t16 * 8388608) + (((t7.y << t16) * 8192) & 8380416) : 0) & 2147475456)), asfloat(t18) * packing.y, 1);
}
