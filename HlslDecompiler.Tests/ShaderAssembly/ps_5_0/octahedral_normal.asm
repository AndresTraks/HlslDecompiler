ps_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_resource_texture2d (float,float,float,float) t0
dcl_input_ps_siv linear noperspective v0.xy, position
dcl_output o0
dcl_temps 2
ftoi r0.xy, v0.xy
mov r0.zw, l(0, 0, 0, 0)
ld_indexable(texture2d)(float,float,float,float) r0.xy, r0.xy, t0.xy
mad r0.xy, r0.xy, l(2, 2, 0, 0), l(-1, -1, 0, 0)
lt r0.zw, l(0, 0, 0, 0), r0.xy
lt r1.xy, r0.xy, l(0, 0, 0, 0)
iadd r0.zw, r0.zw, -r1.xy
itof r0.zw, r0.zw
mul r0.zw, |r0.yx|, r0.zw
add r1.x, -|r0.x|, l(1)
add r1.z, -|r0.y|, r1.x
lt r1.w, r1.z, l(0)
and r0.zw, r0.zw, r1.ww
add r1.xy, r0.zw, r0.xy
dp3 r0.x, r1.xyz, r1.xyz
rsq r0.x, r0.x
mul r0.xyz, r0.xxx, r1.xyz
dp3_sat o0, r0.xyz, -cb0[0].xyz
ret
