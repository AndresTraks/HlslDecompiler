cs_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[3], immediateIndexed
dcl_sampler s0, mode_default
dcl_resource_texture3d (float,float,float,float) t0
dcl_uav_typed_texture3d (float,float,float,float) u0
dcl_input vThreadID.xyz
dcl_temps 4
dcl_thread_group 4, 4, 4
uge r0.xyz, vThreadID.xyz, cb0[2].xyz
or r0.x, r0.y, r0.x
or r0.x, r0.z, r0.x
if_nz r0.x
ret
endif
utof r0.xyz, vThreadID.xyz
add r0.xyz, r0.xyz, l(0.5, 0.5, 0.5, 0)
utof r1.xyz, cb0[2].xyz
div r0.xyz, r0.xyz, r1.xyz
mad r0.xyz, r0.xyz, l(2, 2, 2, 0), l(-1, -1, -1, 0)
dp3 r0.w, r0.xyz, r0.xyz
rsq r0.w, r0.w
mul r0.xyz, r0.www, r0.xyz
mov r1, l(0, 0, 0, 1)
mov r0.w, l(0)
loop
uge r2.x, r0.w, l(16)
breakc_nz r2.x
utof r2.x, r0.w
mul r2.x, r2.x, cb0[0].w
mad r2.xyz, r0.xyz, r2.xxx, cb0[0].xyz
mul r2.xyz, r2.xyz, l(0.100000001, 0.100000001, 0.100000001, 0)
sample_l_indexable(texture3d)(float,float,float,float) r2.x, r2.x, t0.x, s0, l(0)
mov_sat r2.x, r2.x
mul r2.x, r2.x, cb0[1].w
mul r2.y, -r2.x, cb0[0].w
mul r2.y, r2.y, l(1.44269502)
exp r2.y, r2.y
mul r2.xzw, r2.xxx, cb0[1].xyz
mul r2.xzw, r1.www, r2.xzw
mad r2.xzw, r2.xzw, cb0[0].www, r1.xyz
mul r2.y, r1.w, r2.y
lt r3.x, r2.y, l(0.00999999978)
if_nz r3.x
mov r1, r2.xzwy
break
endif
iadd r0.w, r0.w, l(1)
mov r1, r2.xzwy
endloop
store_uav_typed u0, vThreadID.xyzz, r1
ret
