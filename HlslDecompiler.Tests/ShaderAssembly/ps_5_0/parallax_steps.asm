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
utof r0.x, cb0[0].y
div r0.x, l(1, 1, 1, 1), r0.x
max r0.y, |v2.z|, l(0.0000999999975)
div r0.yz, v2.xy, r0.yy
mul r0.yz, r0.yz, cb0[0].xx
mov r1.xy, v1.xy
mov r0.w, l(1)
mov r1.zw, l(0, 0, 1, 1)
mov r2.x, l(0)
loop
uge r2.y, r2.x, cb0[0].y
breakc_nz r2.y
sample_l_indexable(texture2d)(float,float,float,float) r2.y, r1.y, t0.x, s0, l(0)
ge r2.z, r2.y, r0.w
if_nz r2.z
mov r1.xyw, r1.xyz
mov r1.z, r2.y
break
endif
mad r1.xy, -r0.yz, r0.xx, r1.xy
add r0.w, -r0.x, r0.w
iadd r2.x, r2.x, l(1)
mov r1.w, r1.z
mov r1.z, r2.y
endloop
add r1.z, -r0.w, r1.z
add r0.w, r0.x, r0.w
add r0.w, -r0.w, r1.w
add r0.w, -r0.w, r1.z
max r0.w, r0.w, l(0.00000999999975)
div r0.w, r1.z, r0.w
mad r0.xy, r0.yz, r0.xx, r1.xy
add r1.xy, -r0.xy, r1.xy
mad r0.xy, r0.ww, r1.xy, r0.xy
sample_indexable(texture2d)(float,float,float,float) o0, r0.xyxx, t1, s0
ret
