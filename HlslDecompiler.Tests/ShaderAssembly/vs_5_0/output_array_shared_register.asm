vs_5_0
dcl_globalFlags refactoringAllowed
dcl_input v0
dcl_input v1.xyz
dcl_output_siv o0, position
dcl_output o1.x
dcl_output o1.yz
dcl_output o2.x
dcl_output o3.x
mov o0, v0
mov o1.x, v1.x
mov o1.yz, v0.xy
add o2.x, v1.y, v1.y
add o3.x, v1.z, l(1)
ret
