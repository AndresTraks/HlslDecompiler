ps_4_0
dcl_constantbuffer cb0[3], immediateIndexed
dcl_sampler s0, mode_default
dcl_resource_texture2d (float,float,float,float) t0
dcl_resource_texture2d (float,float,float,float) t1
dcl_resource_texture2d (float,float,float,float) t2
dcl_resource_texture2d (float,float,float,float) t3
dcl_input_ps linear v1.xyz
dcl_input_ps linear v2.xy
dcl_input_ps linear v2.z
dcl_output o0
dcl_temps 3
sample r0, v2.xyxx, t3, s0
add r0.w, r0.y, r0.x
add r0.w, r0.z, r0.w
max r0.w, r0.w, l(0.0000999999975)
div r0.xyz, r0.xyz, r0.www
mul r1, v2.xyxy, cb0[0].xxyy
sample r2, r1.zwzz, t1, s0
sample r1, r1.xyxx, t0, s0
mul r2.xyz, r0.yyy, r2.xyz
mad r0.xyw, r1.xyz, r0.xxx, r2.xyz
mul r1.xy, v2.xy, cb0[0].zz
sample r1, r1.xyxx, t2, s0
mad r0.xyz, r1.xyz, r0.zzz, r0.xyw
dp3 r0.w, v1.xyz, v1.xyz
rsq r0.w, r0.w
mul r1.xyz, r0.www, v1.xyz
dp3_sat r0.w, r1.xyz, -cb0[1].xyz
mad r0.w, r0.w, l(0.800000012), l(0.200000003)
mul r1.xyz, r0.www, r0.xyz
mad r0.xyz, -r0.xyz, r0.www, cb0[2].yzw
add r0.w, v2.z, -cb0[1].w
add r1.w, -cb0[1].w, cb0[2].x
div_sat r0.w, r0.w, r1.w
mad o0.xyz, r0.www, r0.xyz, r1.xyz
mov o0.w, l(1)
ret
