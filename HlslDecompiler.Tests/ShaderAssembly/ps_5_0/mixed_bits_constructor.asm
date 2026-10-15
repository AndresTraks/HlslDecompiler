ps_5_0
dcl_globalFlags refactoringAllowed
dcl_sampler s0, mode_comparison
dcl_resource_texture2d (uint,uint,uint,uint) t0
dcl_resource_texture2d (float,float,float,float) t1
dcl_input_ps_siv linear noperspective v0.xy, position
dcl_input_ps linear v1.xy
dcl_output o0
dcl_temps 1
ftoi r0.xy, v0.xy
mov r0.zw, l(0, 0, 0, 0)
ld_indexable(texture2d)(uint,uint,uint,uint) r0.xy, r0.xy, t0.xy
and r0.z, r0.x, l(255)
utof o0.y, r0.z
mov o0.w, l(1)
sample_c_lz_indexable(texture2d)(float,float,float,float) o0.x, v1.x, t1.x, s0, r0.y
ushr o0.z, r0.x, l(8)
ret
