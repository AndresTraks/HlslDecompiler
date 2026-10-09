ps_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[6], immediateIndexed
dcl_sampler s0, mode_default
dcl_resource_texture2d (float,float,float,float) t0
dcl_resource_texture2d (float,float,float,float) t1
dcl_input_ps linear v1.xyz
dcl_input_ps linear v2.xyz
dcl_output o0
dcl_temps 5
add r0.xyz, v1.xyz, -cb0[4].xyz
dp3 r0.w, r0.xyz, r0.xyz
rsq r0.w, r0.w
mul r0.xyz, r0.www, r0.xyz
dp3 r0.w, v2.xyz, v2.xyz
rsq r0.w, r0.w
mul r1.xyz, r0.www, v2.xyz
dp3 r0.w, r0.xyz, r1.xyz
add r0.w, r0.w, r0.w
mad r0.xyz, r1.xyz, -r0.www, r0.xyz
itof r0.w, cb0[5].y
mov r1.w, l(1)
mov r2, l(0, 0, 0, 0)
mov r3.x, l(1)
loop
ilt r3.y, cb0[5].y, r3.x
breakc_nz r3.y
itof r3.y, r3.x
mul r3.y, r3.y, cb0[4].w
div r3.y, r3.y, r0.w
mad r1.xyz, r0.xyz, r3.yyy, v1.xyz
dp4 r4.x, r1, cb0[0]
dp4 r4.y, r1, cb0[1]
dp4 r3.y, r1, cb0[3]
div r3.zw, r4.xy, r3.yy
mad r3.zw, r3.zw, l(0, 0, 0.5, -0.5), l(0, 0, 0.5, 0.5)
lt r4.xy, r3.zw, l(0, 0, 0, 0)
or r4.x, r4.y, r4.x
lt r4.yz, l(0, 1, 1, 0), r3.zw
or r4.y, r4.z, r4.y
or r4.x, r4.y, r4.x
if_nz r4.x
break
endif
dp4 r1.x, r1, cb0[2]
sample_l_indexable(texture2d)(float,float,float,float) r1.y, r3.w, t1.x, s0, l(0)
div r1.x, r1.x, r3.y
lt r1.z, r1.y, r1.x
add r1.x, -r1.y, r1.x
lt r1.x, r1.x, cb0[5].x
and r1.x, r1.x, r1.z
if_nz r1.x
sample_l_indexable(texture2d)(float,float,float,float) r2.xyz, r3.zwz, t0.xyz, s0, l(0)
mov r2.w, l(1)
break
endif
iadd r3.x, r3.x, l(1)
mov r2, l(0, 0, 0, 0)
endloop
mul o0.xyz, r2.www, r2.xyz
mov o0.w, r2.w
ret
