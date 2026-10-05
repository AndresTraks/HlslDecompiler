ps_5_0
dcl_globalFlags refactoringAllowed
dcl_immediateConstantBuffer { { 1, 0, 0, 0 }, { 0, 1, 0, 0 }, { 0, 0, 1, 0 }, { 0, 0, 0, 1 } }
dcl_constantbuffer CB0[18], dynamicIndexed
dcl_sampler s0, mode_comparison
dcl_sampler s1, mode_default
dcl_resource_texture2darray (float,float,float,float) t0
dcl_resource_texture2d (float,float,float,float) t1
dcl_input_ps linear v1.xyz
dcl_input_ps linear v2.xyz
dcl_input_ps linear v3.xy
dcl_input_ps linear v3.z
dcl_output o0
dcl_temps 3
mov r0.xy, l(3, 0, 0, 0)
loop
uge r0.z, r0.y, l(4)
breakc_nz r0.z
dp4 r0.z, cb0[16], icb[r0.y]
lt r0.z, v3.z, r0.z
if_nz r0.z
mov r0.x, r0.y
break
endif
iadd r0.y, r0.y, l(1)
mov r0.x, l(3)
endloop
ishl r0.y, r0.x, l(2)
mov r1.xyz, v1.xyz
mov r1.w, l(1)
dp4 r2.x, r1, cb0[r0.y]
dp4 r2.y, r1, cb0[r0.y + 1]
dp4 r2.z, r1, cb0[r0.y + 2]
dp4 r0.y, r1, cb0[r0.y + 3]
div r0.yzw, r2.xyz, r0.yyy
mad r1.xy, r0.yz, l(0.5, -0.5, 0, 0), l(0.5, 0.5, 0, 0)
utof r1.z, r0.x
add r0.x, r0.w, -cb0[17].w
sample_c_lz_indexable(texture2darray)(float,float,float,float) r0.x, r1.x, t0.x, s0, r0.x
dp3 r0.y, v2.xyz, v2.xyz
rsq r0.y, r0.y
mul r0.yzw, r0.yyy, v2.xyz
dp3_sat r0.y, r0.yzw, -cb0[17].xyz
sample_indexable(texture2d)(float,float,float,float) r1, v3.xyxx, t1, s1
mad r0.x, r0.y, r0.x, l(0.100000001)
mul o0, r0.x, r1
ret
