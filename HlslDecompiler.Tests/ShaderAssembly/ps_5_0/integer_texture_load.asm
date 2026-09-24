ps_5_0
dcl_globalFlags refactoringAllowed
dcl_resource_texture2d (uint,uint,uint,uint) t0
dcl_resource_texture2d (sint,sint,sint,sint) t1
dcl_output o0
dcl_temps 1
mov o0.w, l(1)
ld_indexable(texture2d)(uint,uint,uint,uint) r0.xy, l(1, 2, 0, 0), t0.xz
ld_indexable(texture2d)(sint,sint,sint,sint) r0.zw, l(3, 4, 0, 0), t1.yw
iadd r0.x, r0.z, r0.x
itof o0.z, r0.w
utof o0.xy, r0.xy
ret
