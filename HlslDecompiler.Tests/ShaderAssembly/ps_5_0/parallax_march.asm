ps_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_sampler s0, mode_default
dcl_resource_texture2d (float,float,float,float) t0
dcl_input_ps linear v0.xy
dcl_input_ps linear v1.xyz
dcl_output o0
dcl_temps 3
div r0.xy, v1.xy, v1.zz
mul r0.xy, r0.xy, cb0[0].xx
itof r0.z, cb0[0].y
div r0.xy, r0.xy, r0.zz
div r0.z, l(1, 1, 1, 1), r0.z
sample_l_indexable(texture2d)(float,float,float,float) r0.w, v0.x, t0.x, s0, l(0)
mov r1.x, l(0)
mov r1.y, r0.w
mov r1.zw, v0.xy
loop
ge r2.x, r1.x, r1.y
breakc_nz r2.x
add r1.zw, -r0.xy, r1.zw
sample_l_indexable(texture2d)(float,float,float,float) r1.y, r1.w, t0.x, s0, l(0)
add r1.x, r0.z, r1.x
endloop
mov o0.xyz, r1.zwx
mov o0.w, l(1)
ret
