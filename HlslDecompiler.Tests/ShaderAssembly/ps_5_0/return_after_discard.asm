ps_5_0
dcl_globalFlags refactoringAllowed
dcl_uav_typed_buffer (uint,uint,uint,uint) u1
dcl_input_ps linear v0
dcl_input_ps constant v1.x
dcl_output o0
dcl_temps 1
if_z v1.x
add o0, v0, v0
store_uav_typed u1, l(0, 0, 0, 0), v0.z
lt r0.x, v0.y, l(0)
discard_nz r0.x
ret
else
mov o0, -v0
ret
endif
ret
