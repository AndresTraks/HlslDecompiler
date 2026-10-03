ps_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_sampler s0, mode_default
dcl_resource_buffer (mixed,mixed,mixed,mixed) t0
dcl_resource_texturecubearray (float,float,float,float) t1
dcl_output o0
dcl_temps 2
mov r0.xyz, cb0[0].yzw
mov r0.w, l(2)
sample_indexable(texturecubearray)(float,float,float,float) r0, r0, t1, s0
ld_indexable(buffer)(mixed,mixed,mixed,mixed) r1, cb0[0].x, t0
add o0, r0, r1
ret
