ps_5_0
dcl_globalFlags refactoringAllowed
dcl_resource_texture2d (uint,uint,uint,uint) t0
dcl_output o0
dcl_temps 1
ld_indexable(texture2d)(uint,uint,uint,uint) r0, l(1, 2, 0, 0), t0
utof o0, r0.zxwy
ret
