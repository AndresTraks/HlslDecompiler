ds_5_0
dcl_input_control_point_count 0
dcl_tessellator_domain domain_tri
dcl_globalFlags refactoringAllowed
dcl_input vpc0.y
dcl_input vpc4
dcl_input vDomain.x
dcl_input vPrim
dcl_output_siv o0, position
dcl_temps 2
add r0.x, vpc0.y, vDomain.x
mad r0, vpc4, r0.x, vpc4
utof r1.x, vPrim
add o0, r0, r1.x
ret
