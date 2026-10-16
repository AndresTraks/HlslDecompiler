ps_5_0
dcl_globalFlags refactoringAllowed
dcl_sampler s0, mode_default
dcl_resource_texture2d (uint,uint,uint,uint) t0
dcl_resource_texture2d (sint,sint,sint,sint) t1
dcl_input_ps_siv linear noperspective v0.xy, position
dcl_output o0
dcl_temps 1
ftoi r0.xy, v0.xy
mov r0.zw, l(0, 0, 0, 0)
ld_indexable(texture2d)(sint,sint,sint,sint) r0.z, r0.z, t1.x
ld_indexable(texture2d)(uint,uint,uint,uint) r0.x, r0.x, t0.x
itof r0.y, r0.z
mul o0.y, r0.y, l(0.5)
gather4_indexable(texture2d)(uint,uint,uint,uint) r0.yzw, v0.yxx, t0.xyw, s0.xxx
iadd r0.y, r0.w, r0.y
xor o0.w, r0.z, r0.x
mov o0.x, r0.x
utof o0.z, r0.y
ret
