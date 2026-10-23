ps_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[9], immediateIndexed
dcl_sampler s0, mode_default
dcl_resource_texture2d (float,float,float,float) t0
dcl_resource_texture2d (float,float,float,float) t1
dcl_input_ps_siv linear noperspective v0.xy, position
dcl_input_ps linear v1.xy
dcl_input_ps linear v2.xyz
dcl_output o0
dcl_temps 4
ftoi r0.xy, v0.xy
mov r0.zw, l(0, 0, 0, 0)
ld_indexable(texture2d)(float,float,float,float) r0.z, r0.z, t0.x
mad r0.xy, v1.xy, l(2, -2, 0, 0), l(-1, 1, 0, 0)
mov r0.w, l(1)
dp4 r1.x, r0, cb0[0]
dp4 r1.y, r0, cb0[1]
dp4 r1.z, r0, cb0[2]
dp4 r1.w, r0, cb0[3]
div r0, r1, r1.w
dp4 r1.y, r0, cb0[5]
dp4 r1.x, r0, cb0[4]
dp4 r1.z, r0, cb0[6]
add r2.xyz, -|r1.xyz|, l(0.5, 0.5, 0.5, 0)
lt r2.xyz, r2.xyz, l(0, 0, 0, 0)
or r0.w, r2.y, r2.x
or r0.w, r2.z, r0.w
discard_nz r0.w
deriv_rty_coarse r2.xyz, r0.zxy
deriv_rtx_coarse r0.xyz, r0.yzx
mul r3.xyz, r0.xyz, r2.xyz
mad r0.xyz, r2.zxy, r0.yzx, -r3.xyz
dp3 r0.w, r0.xyz, r0.xyz
rsq r0.w, r0.w
mul r0.xyz, r0.www, r0.xyz
dp3 r0.x, r0.xyz, v2.xyz
add r0.x, r0.x, -cb0[8].z
add r0.y, -cb0[8].z, l(1)
div_sat r0.x, r0.x, r0.y
udiv r2.x, r3.x, cb0[8].x, cb0[8].y
utof r3.x, r3.x
utof r3.y, r2.x
add r0.yz, r1.xz, r3.xy
add r0.yz, r0.yz, l(0, 0.5, 0.5, 0)
utof r0.w, cb0[8].y
div r0.yz, r0.yz, r0.ww
sample_indexable(texture2d)(float,float,float,float) r1, r0.yzyy, t1, s0
mul o0.w, r0.x, r1.w
mov o0.xyz, r1.xyz
ret
