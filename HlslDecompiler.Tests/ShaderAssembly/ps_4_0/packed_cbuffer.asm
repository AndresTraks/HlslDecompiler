ps_4_0
dcl_constantbuffer cb0[2], immediateIndexed
dcl_sampler s0, mode_default
dcl_resource_texture2d (float,float,float,float) t0
dcl_resource_texture2d (float,float,float,float) t1
dcl_resource_texture2d (float,float,float,float) t2
dcl_resource_texture2d (float,float,float,float) t3
dcl_input_ps linear v1.xyz
dcl_input_ps linear v2.xyw
dcl_input_ps linear v3.xy
dcl_output o0
dcl_temps 3
add r0.xyz, -v1.xyz, cb0[0].yzw
dp3 r0.w, r0.xyz, r0.xyz
rsq r0.w, r0.w
mul r0.xyz, r0.www, r0.xyz
mul r1.xw, cb0[0].xx, l(0.0199999996, 0, 0, 0.0130000003)
mov r1.yz, l(0, 0, 0, 0)
add r1.xy, r1.xy, v3.xy
mad r1.zw, v3.xy, l(0, 0, 1.70000005, 1.70000005), -r1.zw
sample r2, r1.zwzz, t1, s0
sample r1, r1.xyxx, t0, s0
mad r1.xyz, r1.xyz, l(2, 2, 2, 0), l(-1, -1, -1, 0)
mad r1.xyz, r2.xyz, l(2, 2, 2, 0), r1.xyz
add r1.xyz, r1.xyz, l(-1, -1, -1, 0)
dp3 r0.w, r1.xyz, r1.xyz
rsq r0.w, r0.w
mul r1.xyz, r0.www, r1.xyz
dp3_sat r0.x, r0.xyz, r1.xyz
add r0.x, -r0.x, l(1)
mul r0.y, r0.x, r0.x
mul r0.y, r0.y, r0.y
mul r0.x, r0.y, r0.x
mad r0.x, r0.x, l(0.899999976), l(0.100000001)
div r0.yz, v2.xy, v2.ww
mad r0.yz, r0.yz, l(0, 0.5, 0.5, 0), l(0, 0.5, 0.5, 0)
mad r1.yw, r1.xz, l(0, 0.0299999993, 0, 0.0299999993), r0.yz
mad r0.yz, -r1.xz, l(0, 0.0199999996, 0.0199999996, 0), r0.yz
sample r2, r0.yzyy, t3, s0
sample r1, r1.ywyy, t2, s0
add r0.yzw, -r2.xyz, r1.xyz
mad r0.xyz, r0.xxx, r0.yzw, r2.xyz
mul o0.xyz, r0.xyz, cb0[1].xyz
mov o0.w, l(1)
ret
