ps_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_sampler s0, mode_default
dcl_resource_texture2d (float,float,float,float) t0
dcl_resource_texture2d (float,float,float,float) t1
dcl_input_ps linear v1.xy
dcl_input_ps linear v2.xyz
dcl_output o0
dcl_temps 3
dp3 r0.x, v2.xyz, v2.xyz
rsq r0.x, r0.x
mul r0.xyz, r0.xxx, v2.xyz
itof r1.xy, cb0[0].zy
add r0.w, -r1.x, r1.y
mad r0.w, |r0.z|, r0.w, r1.x
round_z r0.w, r0.w
div r0.w, l(1, 1, 1, 1), r0.w
div r0.xy, r0.xy, r0.zz
mul r0.xy, r0.xy, cb0[0].xx
mul r1.xy, r0.ww, r0.xy
sample_l_indexable(texture2d)(float,float,float,float) r0.z, v1.x, t1.x, s0, l(0)
mov r1.zw, v1.xy
mov r2.x, l(0)
mov r2.y, r0.z
loop
lt r2.z, r2.x, r2.y
lt r2.w, r2.x, l(1)
and r2.z, r2.w, r2.z
breakc_z r2.z
mad r1.zw, -r0.xy, r0.ww, r1.zw
add r2.x, r0.w, r2.x
sample_l_indexable(texture2d)(float,float,float,float) r2.y, r1.w, t1.x, s0, l(0)
endloop
mad r0.xy, r0.xy, r0.ww, r1.zw
add r0.z, -r2.x, r2.y
sample_l_indexable(texture2d)(float,float,float,float) r0.x, r0.x, t1.x, s0, l(0)
add r0.x, -r2.x, r0.x
add r0.x, r0.w, r0.x
add r0.x, -r0.x, r0.z
div r0.x, r0.z, r0.x
mad r0.xy, r0.xx, r1.xy, r1.zw
sample_indexable(texture2d)(float,float,float,float) o0, r0.xyxx, t0, s0
ret
