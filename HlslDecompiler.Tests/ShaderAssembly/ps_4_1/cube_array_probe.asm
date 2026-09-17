ps_4_1
dcl_globalFlags refactoringAllowed
dcl_constantbuffer cb0[1], immediateIndexed
dcl_sampler s0, mode_default
dcl_resource_texturecubearray (float,float,float,float) t0
dcl_resource_texture2d (float,float,float,float) t1
dcl_input_ps linear v1.xy
dcl_input_ps linear v2.xyz
dcl_input_ps linear v3.xyz
dcl_output o0
dcl_temps 3
dp3 r0.x, v2.xyz, v2.xyz
rsq r0.x, r0.x
mul r0.xyz, r0.xxx, v2.xyz
dp3 r0.w, v3.xyz, v3.xyz
rsq r0.w, r0.w
mul r1.xyz, r0.www, v3.xyz
dp3 r0.w, -r1.xyz, r0.xyz
add r0.w, r0.w, r0.w
mad r2.xyz, r0.xyz, -r0.www, -r1.xyz
dp3_sat r0.x, r0.xyz, r1.xyz
add r0.x, -r0.x, l(1)
mov r2.w, cb0[0].x
sample r1, v1.xyxx, t1, s0
mul r0.y, r1.w, cb0[0].y
sample_l r0.yzw, r2.yzw, t0.xyz, s0, r0.yyy
add r0.yzw, -r1.xyz, r0.yzw
mul r1.w, r0.x, r0.x
mul r1.w, r1.w, r1.w
mul r0.x, r0.x, r1.w
mad r0.x, r0.x, l(0.959999979), l(0.0399999991)
mad o0.xyz, r0.xxx, r0.yzw, r1.xyz
mov o0.w, l(1)
ret
