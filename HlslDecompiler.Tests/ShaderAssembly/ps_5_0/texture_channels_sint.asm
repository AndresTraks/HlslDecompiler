ps_5_0
dcl_globalFlags refactoringAllowed
dcl_resource_texture2d (sint,sint,sint,sint) t0
dcl_output o0
dcl_temps 1
ld_indexable(texture2d)(sint,sint,sint,sint) r0, l(3, 4, 0, 0), t0
itof o0, r0.ywxz
ret
