using RosMessageTypes.Std;
using Unity.Robotics.ROSTCPConnector.MessageGeneration;

/// <summary>
/// SportRequestMsg - Matches unitree_api/msg/Request ROS message structure
/// Used to send gesture commands to Unitree Go2 robot via ROS topic /api/sport/request
/// </summary>
[System.Serializable]
public class SportRequestMsg : Message
{
    public const string k_RosMessageName = "unitree_api/Request";
    public override string RosMessageName => k_RosMessageName;

    public RequestHeader header;
    public string parameter;

    public SportRequestMsg()
    {
        header = new RequestHeader();
        parameter = "{}";
    }
}

[System.Serializable]
public class RequestHeader : Message
{
    public const string k_RosMessageName = "unitree_api/RequestHeader";
    public override string RosMessageName => k_RosMessageName;

    public RequestIdentity identity;
    
    public RequestHeader() 
    { 
        identity = new RequestIdentity(); 
    }
}

[System.Serializable]
public class RequestIdentity : Message
{
    public const string k_RosMessageName = "unitree_api/RequestIdentity";
    public override string RosMessageName => k_RosMessageName;

    public int api_id;
    
    public RequestIdentity() 
    { 
        api_id = 0; 
    }
}
